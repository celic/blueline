# == Schema Information
#
# Table name: goalie_stats
#
#  id              :integer          not null, primary key
#  player_id       :integer
#  verdict         :integer
#  goals_against   :integer
#  shots_against   :integer
#  saves           :integer
#  save_percentage :decimal(5, 2)
#  shutout         :boolean
#  pim             :integer
#  toi             :integer
#  game_id         :integer
#  team_id         :integer
#
# Indexes
#
#  index_goalie_stats_on_game_id    (game_id)
#  index_goalie_stats_on_player_id  (player_id)
#

FactoryGirl.define do
  factory :goalie_stat do
    
  end

end
