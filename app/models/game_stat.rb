# == Schema Information
#
# Table name: game_stats
#
#  id          :integer          not null, primary key
#  team_id     :integer
#  opponent_id :integer
#  home        :boolean
#  decision    :integer
#  date        :date
#  goals       :integer
#  ppg         :integer
#  shg         :integer
#  evg         :integer
#  shots       :integer
#  pim         :integer
#
# Indexes
#
#  index_game_stats_on_opponent_id  (opponent_id)
#  index_game_stats_on_team_id      (team_id)
#

class GameStat < ActiveRecord::Base
	extend EnumerateIt

	## Relationships
	belongs_to :team
	belongs_to :opponent, class_name: 'Team'

	has_enumeration_for :decision, with: Enums::Decision, helpers: true
end
