# == Schema Information
#
# Table name: players
#
#  id       :integer          not null, primary key
#  name     :string
#  position :integer
#  key      :string
#  team_id  :integer
#
# Indexes
#
#  index_players_on_key  (key) UNIQUE
#

class Player < ActiveRecord::Base
  extend EnumerateIt

  belongs_to :team

  has_many :player_stats
  has_many :goalie_stats

  has_enumeration_for :position, with: Enums::Position

  scope :forwards, -> { where(position: Enums::Position.forwards) }
  scope :defensemen, -> { where(position: Enums::Position::D) }
  scope :goalies, -> { where(position: Enums::Position::G) }

  def stat_class
    goalie? ? GoalieStat : PlayerStat
  end

  def stats
    stat_class.where(player: self)
  end

  def add_stats!(game, values)

    # dont add duplicate stats
    return if stat_class.exists?(player: self, game: game)

    stat_class.create! values.merge(player: self, game: game)
  end

  def forward?
    Enums::Position.forward?(position)
  end

  def defenseman?
    Enums::Position.defenseman?(position)
  end

  def skater?
    !goalie?
  end

  def goalie?
    Enums::Position.goalie?(position)
  end
end
