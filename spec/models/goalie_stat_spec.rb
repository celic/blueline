# == Schema Information
#
# Table name: goalie_stats
#
#  id              :integer          not null, primary key
#  player_id       :integer
#  team_id         :integer
#  opponent_id     :integer
#  home            :boolean
#  decision        :integer
#  verdict         :integer
#  goals_against   :integer
#  shots_against   :integer
#  saves           :integer
#  save_percentage :decimal(5, 2)
#  shutout         :boolean
#  pim             :integer
#  toi             :integer
#  date            :date
#
# Indexes
#
#  index_goalie_stats_on_player_id  (player_id)
#  index_goalie_stats_on_team_id    (team_id)
#

require 'rails_helper'

RSpec.describe GoalieStat, type: :model do
  pending "add some examples to (or delete) #{__FILE__}"
end
